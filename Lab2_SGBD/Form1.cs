using System;
using System.Data;
using System.Windows.Forms;
using System.Configuration;
using System.Data.SqlClient;
using System.Collections.Generic;

namespace Lab2_SGBD
{
    public partial class Form1 : Form
    {
        SqlConnection sqlConnection;
        string connectionString;
        string parentTable;
        string childTable;
        string parentKey;
        string childKey;
        string foreignKey;
        List<String> parentColumns = new List<String>();
        List<String> childColumns = new List<String>();

        //initializes the form
        public Form1()
        {
            configure();

            InitializeComponent();

            buildInput();

            inputReset();
        }

        //loads configuration data and information about the tables in use
        private void configure()
        {
            //configuration data is loaded from App.config
            connectionString = ConfigurationManager.ConnectionStrings["connectionString"].ConnectionString;
            parentTable = ConfigurationManager.AppSettings["parent"];
            childTable = ConfigurationManager.AppSettings["child"];

            //a SqlConnection object and a SqlCommand object are declared for later use
            sqlConnection = new SqlConnection(connectionString);
            SqlCommand sqlCommand;

            //string for an sql command which selects the name of the parent table's primary key
            string selectParentKey = "select COLUMN_NAME " +
                "from INFORMATION_SCHEMA.KEY_COLUMN_USAGE " +
                "where TABLE_NAME like '" + parentTable + "' " +
                "and CONSTRAINT_NAME like 'PK%'";

            //string for an sql command which selects the name of the child table's primary key
            string selectChildKey = "select COLUMN_NAME " +
                "from INFORMATION_SCHEMA.KEY_COLUMN_USAGE " +
                "where TABLE_NAME like '" + childTable + "' " +
                "and CONSTRAINT_NAME like 'PK%'";

            //string for an sql command which selects the name of the child table's foreign key
            string selectForeignKey = "select COLUMN_NAME " +
                "from INFORMATION_SCHEMA.KEY_COLUMN_USAGE " +
                "where TABLE_NAME like '" + childTable + "' " +
                "and CONSTRAINT_NAME like 'FK%'";

            //string for an sql command which selects the names of the parent table's columns
            string selectParentColumns = "select COLUMN_NAME " +
                "from INFORMATION_SCHEMA.COLUMNS " +
                "where TABLE_NAME = '" + parentTable + "'";

            //string for an sql command which selects the names of the child table's columns
            string selectChildColumns = "select COLUMN_NAME " +
                "from INFORMATION_SCHEMA.COLUMNS " +
                "where TABLE_NAME = '" + childTable + "'";

            try
            {
                sqlConnection.Open();

                //parent table's key is selected,
                //result is processed as a scalar
                sqlCommand = new SqlCommand(selectParentKey, sqlConnection);
                parentKey = (string)sqlCommand.ExecuteScalar();

                //child table's key is selected,
                //result is processed as a scalar
                sqlCommand = new SqlCommand(selectChildKey, sqlConnection);
                childKey = (string)sqlCommand.ExecuteScalar();

                //child table's foreign key is selected,
                //result is processed as a scalar
                sqlCommand = new SqlCommand(selectForeignKey, sqlConnection);
                foreignKey = (string)sqlCommand.ExecuteScalar();

                //parent table's columns are selected,
                //result is transformed into a list of strings
                sqlCommand = new SqlCommand(selectParentColumns, sqlConnection);
                SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();

                while (sqlDataReader.Read())
                {
                    parentColumns.Add(sqlDataReader.GetString(0));
                }
                sqlDataReader.Close();

                //child table's columns are selected,
                //result is transformed into a list of strings
                sqlCommand = new SqlCommand(selectChildColumns, sqlConnection);
                sqlDataReader = sqlCommand.ExecuteReader();

                while (sqlDataReader.Read())
                {
                    childColumns.Add(sqlDataReader.GetString(0));
                }
                sqlDataReader.Close();

                //connection is closed
                sqlConnection.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                sqlConnection.Close();
            }
        }

        //creates the labels and text boxes necessary for database interaction
        private void buildInput()
        {
            //an adequate number of labels and text boxes must be created
            for (int i = 0; i < childColumns.Count; i++)
            {
                //each label displays the name of its respective column
                Label label = new Label();
                label.Text = childColumns[i] + ":";
                panel.Controls.Add(label);

                //each text box is named after its respective column
                TextBox textBox = new TextBox();
                textBox.Name = childColumns[i];
                panel.Controls.Add(textBox);

                label.Location = new System.Drawing.Point(0, 4 + 24 * i);
                textBox.Location = new System.Drawing.Point(120, 24 * i);
            }
        }

        //disables all text fields and buttons, clears text
        private void inputReset()
        {
            foreach (Control control in panel.Controls)
            {
                if (control is TextBox)
                {
                    ((TextBox)control).Clear();
                    ((TextBox)control).Enabled = false;
                }
            }
            insertButton.Enabled = false;
            updateButton.Enabled = false;
            deleteButton.Enabled = false;
        }

        //loads and displays data from the parent and child tables (mapped to the "Connect" button) 
        private void loadData(object sender, EventArgs e)
        {
            //a DataSet is created, it is an in-memory cache that stores information from the database as DataTable objects
            DataSet dataSet = new DataSet();

            //SqlDataAdapter objects are needed for selection from the parent and child tables
            SqlDataAdapter parentAdapter = new SqlDataAdapter("select * from " + parentTable, connectionString);
            SqlDataAdapter childAdapter = new SqlDataAdapter("select * from " + childTable, connectionString);

            //a DataTable named "parent" is added to the DataSet and is populated using parentAdapter
            parentAdapter.Fill(dataSet, "parent");
            //a DataTable named "child" is added to the DataSet and is populated using childAdapter
            childAdapter.Fill(dataSet, "child");

            //foreign key constraint is modeled using a DataRelation object
            DataRelation dataRelation = new DataRelation("FK_parent_child",
                dataSet.Tables["parent"].Columns[parentKey],
                dataSet.Tables["child"].Columns[foreignKey]);

            //if the relation is built correctly it will be added to the DataSet, otherwise an error message is shown
            try
            {
                dataSet.Relations.Add(dataRelation);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            //a binding source for dataGridView1 is created
            BindingSource bindingSource1 = new BindingSource();
            //data is provided by the DataSet
            bindingSource1.DataSource = dataSet;
            //connector binds to the "parent" table
            bindingSource1.DataMember = "parent";

            //a binding source for dataGridView2 is created
            BindingSource bindingSource2 = new BindingSource();
            //data is provided by the previous binding source
            bindingSource2.DataSource = bindingSource1;
            //connector binds to the children of the selected tuple in dataGridView1
            bindingSource2.DataMember = "FK_parent_child";

            dataGridView1.DataSource = bindingSource1;
            dataGridView2.DataSource = bindingSource2;
        }

        //enables interaction with GridView1 (CellClick)
        //if a valid parent-tuple is clicked, fills in the tuple's key and allows the insertion of a new child-tuple
        private void clickView1(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                inputReset();

                //the parent key's position must be searched for,
                //as it may not always be the first element in the table
                int keyPosition = -1;

                for (int i = 0; i < parentColumns.Count; i++)
                {
                    if (parentColumns[i].Equals(parentKey))
                        keyPosition = i;
                }

                //the parent key's value is selected from the clicked row
                string parentKeyValue = dataGridView1[keyPosition, dataGridView1.CurrentCell.RowIndex]
                    .Value.ToString();

                //clicking an invalid row must not enable interaction or fill in information
                if (!String.IsNullOrWhiteSpace(parentKeyValue))
                {
                    foreach (Control control in panel.Controls)
                    {
                        //the text boxes need to be altered
                        if (control is TextBox)
                        {
                            //the foreign key text box is filled in with the parent key's value
                            //and remains disabled
                            if (control.Name.Equals(foreignKey))
                            {
                                ((TextBox)control).Text = parentKeyValue;
                                ((TextBox)control).Enabled = false;
                            }
                            //other text boxes are cleared and enabled
                            else
                            {
                                ((TextBox)control).Clear();
                                ((TextBox)control).Enabled = true;
                            }
                        }
                    }
                    //insertion is currently possible
                    insertButton.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        //enables interaction with GridView2 (CellClick)
        //if a valid child-tuple is clicked, fills in all data and allows the user to update or delete it
        private void clickView2(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                inputReset();

                int rowNumber = dataGridView2.CurrentCell.RowIndex;

                //the child key's and foreign key's positions must be searched for
                int keyPosition = -1;
                int foreignKeyPosition = -1;

                for (int i = 0; i < childColumns.Count; i++)
                {
                    if (childColumns[i].Equals(childKey))
                        keyPosition = i;
                    if (childColumns[i].Equals(foreignKey))
                        foreignKeyPosition = i;
                }

                //clicking an invalid row must not enable interaction or fill in information
                if (!String.IsNullOrWhiteSpace(dataGridView2[keyPosition, rowNumber].Value.ToString()))
                {
                    int controlNumber = 0;
                    foreach (Control control in panel.Controls)
                    {
                        //the text boxes need to be altered
                        if (control is TextBox)
                        {
                            //data from the clicked row is filled in
                            ((TextBox)control).Text = dataGridView2[controlNumber, rowNumber].Value.ToString();
                            ((TextBox)control).Enabled = true;
                            controlNumber++;

                            //the child key and foreign key text boxes must be disabled
                            //as they should not be changed by the user
                            if (control.Name.Equals(childKey) || control.Name.Equals(foreignKey))
                            {
                                ((TextBox)control).Enabled = false;
                            }
                        }
                    }

                    //updates and removals are currently possible
                    updateButton.Enabled = true;
                    deleteButton.Enabled = true;
                }
                //if an invalid row was clicked, the foreign key may be filled in as it is still relevant
                else
                {
                    foreach (Control control in panel.Controls)
                    {
                        if (control is TextBox)
                        {
                            if (control.Name.Equals(foreignKey))
                            {
                                ((TextBox)control).Text = dataGridView2[foreignKeyPosition, rowNumber]
                                    .Value.ToString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        //inserts a new tuple into the child table (mapped to the "Insert" button) 
        private void insert(object sender, EventArgs e)
        {
            SqlConnection sqlConnection = new SqlConnection(connectionString);

            try
            {
                //a SqlDataAdapter is needed for insertion
                SqlDataAdapter dataAdapter = new SqlDataAdapter();

                //the parameter set is first created as a list of strings
                List<string> parametrs = new List<string>();
                foreach (string childColumn in childColumns)
                {
                    parametrs.Add("@" + childColumn);
                }
                //the insert command's string is built with the necessary parameters
                string insert = "insert into " + childTable + " values(" + string.Join(", ", parametrs) + ")";
                dataAdapter.InsertCommand = new SqlCommand(insert, sqlConnection);

                //parameter values are extracted from the text boxes
                foreach (Control control in panel.Controls)
                {
                    if (control is TextBox)
                    {
                        if (!String.IsNullOrWhiteSpace(((TextBox)control).Text))
                        {
                            dataAdapter.InsertCommand.Parameters
                                .AddWithValue("@" + control.Name, ((TextBox)control).Text);
                        }
                        //empty text boxes will yield a null value
                        else
                        {
                            dataAdapter.InsertCommand.Parameters
                                .AddWithValue("@" + control.Name, DBNull.Value);
                        }
                    }
                }

                //a connection to the database is established and the command executes
                sqlConnection.Open();
                dataAdapter.InsertCommand.ExecuteNonQuery();
                MessageBox.Show("Inserted data successfully.");
                sqlConnection.Close();

                //in order to see the newly inserted data, the database must be reloaded
                inputReset();
                loadData(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                sqlConnection.Close();
            }
        }

        //updates a tuple from the child table (mapped to the "Update" button) 
        private void update(object sender, EventArgs e)
        {
            SqlConnection sqlConnection = new SqlConnection(connectionString);

            try
            {
                //a SqlDataAdapter is needed for updating
                SqlDataAdapter dataAdapter = new SqlDataAdapter();

                //the parameter set is first created as a list of strings
                List<string> parametrs = new List<string>();
                foreach (string childColumn in childColumns)
                {
                    if (childColumn != childKey && childColumn != foreignKey)
                        parametrs.Add(childColumn + " = @" + childColumn);
                }
                //the update command's string is built with the necessary parameters
                string update = "update " + childTable + " set " + string.Join(", ", parametrs) + " where " + childKey + "= @" + childKey;
                dataAdapter.UpdateCommand = new SqlCommand(update, sqlConnection);

                //parameter values are extracted from the text boxes
                foreach (Control control in panel.Controls)
                {
                    if (control is TextBox)
                    {
                        //the foreign key is not part of this command's parameter set
                        if (control.Name != foreignKey)
                        {
                            if (!String.IsNullOrWhiteSpace(((TextBox)control).Text))
                            {
                                dataAdapter.UpdateCommand.Parameters
                                    .AddWithValue("@" + control.Name, ((TextBox)control).Text);
                            }
                            //empty text boxes will yield a null value
                            else
                            {
                                dataAdapter.UpdateCommand.Parameters
                                    .AddWithValue("@" + control.Name, DBNull.Value);
                            }
                        }
                    }
                }

                //a connection to the database is established and the command executes
                sqlConnection.Open();
                dataAdapter.UpdateCommand.ExecuteNonQuery();
                MessageBox.Show("Updated data successfully.");
                sqlConnection.Close();

                //in order to see the newly inserted data, the database must be reloaded
                inputReset();
                loadData(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                sqlConnection.Close();
            }
        }

        //deletes a tuple from the child table (mapped to the "Delete" button) 
        private void delete(object sender, EventArgs e)
        {
            SqlConnection sqlConnection = new SqlConnection(connectionString);

            try
            {
                //a SqlDataAdapter is needed for deletion
                SqlDataAdapter dataAdapter = new SqlDataAdapter();

                //the delete command's string is declared, it contains only one parameter
                dataAdapter.DeleteCommand = new SqlCommand("delete from " + childTable +
                    " where " + childKey + "= @" + childKey, sqlConnection);

                //The key's value is searched for in the text boxes
                foreach (Control control in panel.Controls)
                {
                    if (control is TextBox)
                    {
                        if (control.Name.Equals(childKey))
                        {
                            dataAdapter.DeleteCommand.Parameters
                                .AddWithValue("@" + childKey, ((TextBox)control).Text);
                        }
                    }
                }

                //a connection to the database is established and the command executes
                sqlConnection.Open();
                dataAdapter.DeleteCommand.ExecuteNonQuery();
                MessageBox.Show("Deleted data successfully.");
                sqlConnection.Close();

                //in order to see the newly inserted data, the database must be reloaded
                inputReset();
                loadData(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                sqlConnection.Close();
            }
        }
    }
}
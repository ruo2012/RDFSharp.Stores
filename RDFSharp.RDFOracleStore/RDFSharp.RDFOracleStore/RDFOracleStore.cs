﻿/*
   Copyright 2012-2019 Marco De Salvo

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

     http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using System;
using Oracle.ManagedDataAccess.Client;
using RDFSharp.Model;

namespace RDFSharp.Store
{

    /// <summary>
    /// RDFOracleStore represents a store backed on Oracle engine
    /// </summary>
    public sealed class RDFOracleStore: RDFStore {

        #region Properties
        /// <summary>
        /// Connection to the Oracle database
        /// </summary>
        internal OracleConnection Connection { get; set; }

        /// <summary>
        /// Utility for getting fields of the connection
        /// </summary>
        private OracleConnectionStringBuilder ConnectionBuilder { get; set; }
        #endregion

        #region Ctors
        /// <summary>
        /// Default-ctor to build an Oracle store instance
        /// </summary>
        public RDFOracleStore(String oracleConnectionString) {
            if(!String.IsNullOrEmpty(oracleConnectionString)) {

                //Initialize store structures
                this.StoreType         = "ORACLE";
                this.Connection        = new OracleConnection(oracleConnectionString);
                this.ConnectionBuilder = new OracleConnectionStringBuilder(this.Connection.ConnectionString);
                this.StoreID           = RDFModelUtilities.CreateHash(this.ToString());

                //Perform initial diagnostics
                this.PrepareStore();

            }
            else {
                throw new RDFStoreException("Cannot connect to Oracle store because: given \"oracleConnectionString\" parameter is null or empty.");
            }
        }
        #endregion

        #region Interfaces
        /// <summary>
        /// Gives the string representation of the Oracle store 
        /// </summary>
        public override String ToString() {
            return base.ToString() + "|SERVER=" + this.Connection.DataSource + ";DATABASE=" + this.Connection.Database;
        }
        #endregion

        #region Methods

        #region Add
        /// <summary>
        /// Merges the given graph into the store within a single transaction, avoiding duplicate insertions
        /// </summary>
        public override RDFStore MergeGraph(RDFGraph graph) {
            if (graph       != null) {
                var graphCtx = new RDFContext(graph.Context);

                //Create command
                var command  = new OracleCommand("INSERT INTO \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\"(\"QuadrupleID\", \"TripleFlavor\", \"Context\", \"ContextID\", \"Subject\", \"SubjectID\", \"Predicate\", \"PredicateID\", \"Object\", \"ObjectID\") SELECT :QID, :TFV, :CTX, :CTXID, :SUBJ, :SUBJID, :PRED, :PREDID, :OBJ, :OBJID FROM DUAL WHERE NOT EXISTS(SELECT \"QuadrupleID\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"QuadrupleID\" = :QID)", this.Connection);
                command.Parameters.Add(new OracleParameter("QID",    OracleDbType.Int64));
                command.Parameters.Add(new OracleParameter("TFV",    OracleDbType.Int32));
                command.Parameters.Add(new OracleParameter("CTX",    OracleDbType.Varchar2, 1000));
                command.Parameters.Add(new OracleParameter("CTXID",  OracleDbType.Int64));
                command.Parameters.Add(new OracleParameter("SUBJ",   OracleDbType.Varchar2, 1000));
                command.Parameters.Add(new OracleParameter("SUBJID", OracleDbType.Int64));
                command.Parameters.Add(new OracleParameter("PRED",   OracleDbType.Varchar2, 1000));
                command.Parameters.Add(new OracleParameter("PREDID", OracleDbType.Int64));
                command.Parameters.Add(new OracleParameter("OBJ",    OracleDbType.Varchar2, 1000));
                command.Parameters.Add(new OracleParameter("OBJID",  OracleDbType.Int64));

                try {

                    //Open connection
                    this.Connection.Open();

                    //Prepare command
                    command.Prepare();

                    //Open transaction
                    command.Transaction = this.Connection.BeginTransaction();

                    //Iterate triples
                    foreach(var triple in graph) {

                        //Valorize parameters
                        command.Parameters["QID"].Value    = RDFModelUtilities.CreateHash(graphCtx         + " " +
                                                                                          triple.Subject   + " " +
                                                                                          triple.Predicate + " " +
                                                                                          triple.Object);
                        command.Parameters["TFV"].Value    = (Int32)triple.TripleFlavor;
                        command.Parameters["CTX"].Value    = graphCtx.ToString();
                        command.Parameters["CTXID"].Value  = graphCtx.PatternMemberID;
                        command.Parameters["SUBJ"].Value   = triple.Subject.ToString();
                        command.Parameters["SUBJID"].Value = triple.Subject.PatternMemberID;
                        command.Parameters["PRED"].Value   = triple.Predicate.ToString();
                        command.Parameters["PREDID"].Value = triple.Predicate.PatternMemberID;
                        command.Parameters["OBJ"].Value    = triple.Object.ToString();
                        command.Parameters["OBJID"].Value  = triple.Object.PatternMemberID;

                        //Execute command
                        command.ExecuteNonQuery();
                    }

                    //Close transaction
                    command.Transaction.Commit();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Rollback transaction
                    command.Transaction.Rollback();

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot insert data into Oracle store because: " + ex.Message, ex);

                }

            }
            return this;
        }

        /// <summary>
        /// Adds the given quadruple to the store, avoiding duplicate insertions
        /// </summary>
        public override RDFStore AddQuadruple(RDFQuadruple quadruple) {
            if (quadruple  != null) {

                //Create command
                var command = new OracleCommand("INSERT INTO \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\"(\"QuadrupleID\", \"TripleFlavor\", \"Context\", \"ContextID\", \"Subject\", \"SubjectID\", \"Predicate\", \"PredicateID\", \"Object\", \"ObjectID\") SELECT :QID, :TFV, :CTX, :CTXID, :SUBJ, :SUBJID, :PRED, :PREDID, :OBJ, :OBJID FROM DUAL WHERE NOT EXISTS(SELECT \"QuadrupleID\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"QuadrupleID\" = :QID)", this.Connection);
                command.Parameters.Add(new OracleParameter("QID",    OracleDbType.Int64));
                command.Parameters.Add(new OracleParameter("TFV",    OracleDbType.Int32));
                command.Parameters.Add(new OracleParameter("CTX",    OracleDbType.Varchar2, 1000));
                command.Parameters.Add(new OracleParameter("CTXID",  OracleDbType.Int64));
                command.Parameters.Add(new OracleParameter("SUBJ",   OracleDbType.Varchar2, 1000));
                command.Parameters.Add(new OracleParameter("SUBJID", OracleDbType.Int64));
                command.Parameters.Add(new OracleParameter("PRED",   OracleDbType.Varchar2, 1000));
                command.Parameters.Add(new OracleParameter("PREDID", OracleDbType.Int64));
                command.Parameters.Add(new OracleParameter("OBJ",    OracleDbType.Varchar2, 1000));
                command.Parameters.Add(new OracleParameter("OBJID",  OracleDbType.Int64));

                //Valorize parameters
                command.Parameters["QID"].Value    = quadruple.QuadrupleID;
                command.Parameters["TFV"].Value    = (Int32)quadruple.TripleFlavor;
                command.Parameters["CTX"].Value    = quadruple.Context.ToString();
                command.Parameters["CTXID"].Value  = quadruple.Context.PatternMemberID;
                command.Parameters["SUBJ"].Value   = quadruple.Subject.ToString();
                command.Parameters["SUBJID"].Value = quadruple.Subject.PatternMemberID;
                command.Parameters["PRED"].Value   = quadruple.Predicate.ToString();
                command.Parameters["PREDID"].Value = quadruple.Predicate.PatternMemberID;
                command.Parameters["OBJ"].Value    = quadruple.Object.ToString();
                command.Parameters["OBJID"].Value  = quadruple.Object.PatternMemberID;

                try {

                    //Open connection
                    this.Connection.Open();

                    //Prepare command
                    command.Prepare();

                    //Open transaction
                    command.Transaction = this.Connection.BeginTransaction();

                    //Execute command
                    command.ExecuteNonQuery();

                    //Close transaction
                    command.Transaction.Commit();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Rollback transaction
                    command.Transaction.Rollback();

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot insert data into Oracle store because: " + ex.Message, ex);

                }

            }
            return this;
        }
        #endregion

        #region Remove
        /// <summary>
        /// Removes the given quadruples from the store
        /// </summary>
        public override RDFStore RemoveQuadruple(RDFQuadruple quadruple) {
            if (quadruple  != null) {

                //Create command
                var command = new OracleCommand("DELETE FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"QuadrupleID\" = :QID", this.Connection);
                command.Parameters.Add(new OracleParameter("QID", OracleDbType.Int64));

                //Valorize parameters
                command.Parameters["QID"].Value = quadruple.QuadrupleID;

                try {

                    //Open connection
                    this.Connection.Open();

                    //Prepare command
                    command.Prepare();

                    //Open transaction
                    command.Transaction = this.Connection.BeginTransaction();

                    //Execute command
                    command.ExecuteNonQuery();

                    //Close transaction
                    command.Transaction.Commit();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Rollback transaction
                    command.Transaction.Rollback();

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot delete data from Oracle store because: " + ex.Message, ex);

                }

            }
            return this;
        }

        /// <summary>
        /// Removes the quadruples with the given context
        /// </summary>
        public override RDFStore RemoveQuadruplesByContext(RDFContext contextResource) {
            if (contextResource != null) {

                //Create command
                var command      = new OracleCommand("DELETE FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ContextID\" = :CTXID", this.Connection);
                command.Parameters.Add(new OracleParameter("CTXID", OracleDbType.Int64));

                //Valorize parameters
                command.Parameters["CTXID"].Value = contextResource.PatternMemberID;

                try {

                    //Open connection
                    this.Connection.Open();

                    //Prepare command
                    command.Prepare();

                    //Open transaction
                    command.Transaction = this.Connection.BeginTransaction();

                    //Execute command
                    command.ExecuteNonQuery();

                    //Close transaction
                    command.Transaction.Commit();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Rollback transaction
                    command.Transaction.Rollback();

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot delete data from Oracle store because: " + ex.Message, ex);

                }

            }
            return this;
        }

        /// <summary>
        /// Removes the quadruples with the given subject
        /// </summary>
        public override RDFStore RemoveQuadruplesBySubject(RDFResource subjectResource) {
            if (subjectResource != null) {

                //Create command
                var command      = new OracleCommand("DELETE FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"SubjectID\" = :SUBJID", this.Connection);
                command.Parameters.Add(new OracleParameter("SUBJID", OracleDbType.Int64));

                //Valorize parameters
                command.Parameters["SUBJID"].Value = subjectResource.PatternMemberID;

                try {

                    //Open connection
                    this.Connection.Open();

                    //Prepare command
                    command.Prepare();

                    //Open transaction
                    command.Transaction = this.Connection.BeginTransaction();

                    //Execute command
                    command.ExecuteNonQuery();

                    //Close transaction
                    command.Transaction.Commit();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Rollback transaction
                    command.Transaction.Rollback();

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot delete data from Oracle store because: " + ex.Message, ex);

                }

            }
            return this;
        }

        /// <summary>
        /// Removes the quadruples with the given predicate
        /// </summary>
        public override RDFStore RemoveQuadruplesByPredicate(RDFResource predicateResource) {
            if (predicateResource != null) {

                //Create command
                var command        = new OracleCommand("DELETE FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"PredicateID\" = :PREDID", this.Connection);
                command.Parameters.Add(new OracleParameter("PREDID", OracleDbType.Int64));

                //Valorize parameters
                command.Parameters["PREDID"].Value = predicateResource.PatternMemberID;

                try {

                    //Open connection
                    this.Connection.Open();

                    //Prepare command
                    command.Prepare();

                    //Open transaction
                    command.Transaction = this.Connection.BeginTransaction();

                    //Execute command
                    command.ExecuteNonQuery();

                    //Close transaction
                    command.Transaction.Commit();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Rollback transaction
                    command.Transaction.Rollback();

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot delete data from Oracle store because: " + ex.Message, ex);

                }

            }
            return this;
        }

        /// <summary>
        /// Removes the quadruples with the given resource as object
        /// </summary>
        public override RDFStore RemoveQuadruplesByObject(RDFResource objectResource) {
            if (objectResource != null) {

                //Create command
                var command     = new OracleCommand("DELETE FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                command.Parameters.Add(new OracleParameter("OBJID", OracleDbType.Int64));
                command.Parameters.Add(new OracleParameter("TFV",   OracleDbType.Int32));

                //Valorize parameters
                command.Parameters["OBJID"].Value = objectResource.PatternMemberID;
                command.Parameters["TFV"].Value   = (Int32)RDFModelEnums.RDFTripleFlavors.SPO;

                try {

                    //Open connection
                    this.Connection.Open();

                    //Prepare command
                    command.Prepare();

                    //Open transaction
                    command.Transaction = this.Connection.BeginTransaction();

                    //Execute command
                    command.ExecuteNonQuery();

                    //Close transaction
                    command.Transaction.Commit();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Rollback transaction
                    command.Transaction.Rollback();

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot delete data from Oracle store because: " + ex.Message, ex);

                }

            }
            return this;
        }

        /// <summary>
        /// Removes the quadruples with the given literal as object
        /// </summary>
        public override RDFStore RemoveQuadruplesByLiteral(RDFLiteral literalObject) {
            if (literalObject != null) {

                //Create command
                var command    = new OracleCommand("DELETE FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                command.Parameters.Add(new OracleParameter("OBJID", OracleDbType.Int64));
                command.Parameters.Add(new OracleParameter("TFV",   OracleDbType.Int32));

                //Valorize parameters
                command.Parameters["OBJID"].Value = literalObject.PatternMemberID;
                command.Parameters["TFV"].Value   = (Int32)RDFModelEnums.RDFTripleFlavors.SPL;

                try {

                    //Open connection
                    this.Connection.Open();

                    //Prepare command
                    command.Prepare();

                    //Open transaction
                    command.Transaction = this.Connection.BeginTransaction();

                    //Execute command
                    command.ExecuteNonQuery();

                    //Close transaction
                    command.Transaction.Commit();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Rollback transaction
                    command.Transaction.Rollback();

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot delete data from Oracle store because: " + ex.Message, ex);

                }

            }
            return this;
        }

        /// <summary>
        /// Clears the quadruples of the store
        /// </summary>
        public override void ClearQuadruples() {

            //Create command
            var command = new OracleCommand("DELETE FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\"", this.Connection);

            try {

                //Open connection
                this.Connection.Open();

                //Prepare command
                command.Prepare();

                //Open transaction
                command.Transaction = this.Connection.BeginTransaction();

                //Execute command
                command.ExecuteNonQuery();

                //Close transaction
                command.Transaction.Commit();

                //Close connection
                this.Connection.Close();

            }
            catch (Exception ex) {

                //Rollback transaction
                command.Transaction.Rollback();

                //Close connection
                this.Connection.Close();

                //Propagate exception
                throw new RDFStoreException("Cannot delete data from Oracle store because: " + ex.Message, ex);

            }

        }
        #endregion

        #region Select
        /// <summary>
        /// Gets a memory store containing quadruples satisfying the given pattern
        /// </summary>
        internal override RDFMemoryStore SelectQuadruples(RDFContext  ctx,
                                                          RDFResource subj,
                                                          RDFResource pred,
                                                          RDFResource obj,
                                                          RDFLiteral  lit) {
            RDFMemoryStore result     = new RDFMemoryStore();
            OracleCommand command     = null;

            //Intersect the filters
            if (ctx                 != null) {
                if (subj            != null) {
                    if (pred        != null) {
                        if (obj     != null) {
                            //C->S->P->O
                            command  = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ContextID\" = :CTXID AND \"SubjectID\" = :SUBJID AND \"PredicateID\" = :PREDID AND \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                            command.Parameters.Add(new OracleParameter("TFV",    OracleDbType.Int32));
                            command.Parameters.Add(new OracleParameter("CTXID",  OracleDbType.Int64));
                            command.Parameters.Add(new OracleParameter("SUBJID", OracleDbType.Int64));
                            command.Parameters.Add(new OracleParameter("PREDID", OracleDbType.Int64));
                            command.Parameters.Add(new OracleParameter("OBJID",  OracleDbType.Int64));
                            command.Parameters["TFV"].Value    = (Int32)RDFModelEnums.RDFTripleFlavors.SPO;
                            command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                            command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                            command.Parameters["PREDID"].Value = pred.PatternMemberID;
                            command.Parameters["OBJID"].Value  = obj.PatternMemberID;
                        }
                        else {
                            if (lit != null) {
                                //C->S->P->L
                                command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ContextID\" = :CTXID AND \"SubjectID\" = :SUBJID AND \"PredicateID\" = :PREDID AND \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                                command.Parameters.Add(new OracleParameter("TFV",    OracleDbType.Int32));
                                command.Parameters.Add(new OracleParameter("CTXID",  OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("SUBJID", OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("PREDID", OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("OBJID",  OracleDbType.Int64));
                                command.Parameters["TFV"].Value    = (Int32)RDFModelEnums.RDFTripleFlavors.SPL;
                                command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                                command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                                command.Parameters["PREDID"].Value = pred.PatternMemberID;
                                command.Parameters["OBJID"].Value  = lit.PatternMemberID;
                            }
                            else {
                                //C->S->P->
                                command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ContextID\" = :CTXID AND \"SubjectID\" = :SUBJID AND \"PredicateID\" = :PREDID", this.Connection);
                                command.Parameters.Add(new OracleParameter("CTXID",  OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("SUBJID", OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("PREDID", OracleDbType.Int64));
                                command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                                command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                                command.Parameters["PREDID"].Value = pred.PatternMemberID;
                            }
                        }
                    }
                    else {
                        if (obj     != null) {
                            //C->S->->O
                            command  = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ContextID\" = :CTXID AND \"SubjectID\" = :SUBJID AND \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                            command.Parameters.Add(new OracleParameter("TFV",    OracleDbType.Int32));
                            command.Parameters.Add(new OracleParameter("CTXID",  OracleDbType.Int64));
                            command.Parameters.Add(new OracleParameter("SUBJID", OracleDbType.Int64));
                            command.Parameters.Add(new OracleParameter("OBJID",  OracleDbType.Int64));
                            command.Parameters["TFV"].Value    = (Int32)RDFModelEnums.RDFTripleFlavors.SPO;
                            command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                            command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                            command.Parameters["OBJID"].Value  = obj.PatternMemberID;
                        }
                        else {
                            if (lit != null) {
                                //C->S->->L
                                command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ContextID\" = :CTXID AND \"SubjectID\" = :SUBJID AND \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                                command.Parameters.Add(new OracleParameter("TFV",    OracleDbType.Int32));
                                command.Parameters.Add(new OracleParameter("CTXID",  OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("SUBJID", OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("OBJID",  OracleDbType.Int64));
                                command.Parameters["TFV"].Value    = (Int32)RDFModelEnums.RDFTripleFlavors.SPL;
                                command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                                command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                                command.Parameters["OBJID"].Value  = lit.PatternMemberID;
                            }
                            else {
                                //C->S->->
                                command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ContextID\" = :CTXID AND \"SubjectID\" = :SUBJID", this.Connection);
                                command.Parameters.Add(new OracleParameter("CTXID",  OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("SUBJID", OracleDbType.Int64));
                                command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                                command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                            }
                        }
                    }
                }
                else {
                    if (pred        != null) {
                        if (obj     != null) {
                            //C->->P->O
                            command  = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ContextID\" = :CTXID AND \"PredicateID\" = :PREDID AND \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                            command.Parameters.Add(new OracleParameter("TFV",    OracleDbType.Int32));
                            command.Parameters.Add(new OracleParameter("CTXID",  OracleDbType.Int64));
                            command.Parameters.Add(new OracleParameter("PREDID", OracleDbType.Int64));
                            command.Parameters.Add(new OracleParameter("OBJID",  OracleDbType.Int64));
                            command.Parameters["TFV"].Value    = (Int32)RDFModelEnums.RDFTripleFlavors.SPO;
                            command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                            command.Parameters["PREDID"].Value = pred.PatternMemberID;
                            command.Parameters["OBJID"].Value  = obj.PatternMemberID;
                        }
                        else {
                            if (lit != null) {
                                //C->->P->L
                                command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ContextID\" = :CTXID AND \"PredicateID\" = :PREDID AND \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                                command.Parameters.Add(new OracleParameter("TFV",    OracleDbType.Int32));
                                command.Parameters.Add(new OracleParameter("CTXID",  OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("PREDID", OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("OBJID",  OracleDbType.Int64));
                                command.Parameters["TFV"].Value    = (Int32)RDFModelEnums.RDFTripleFlavors.SPL;
                                command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                                command.Parameters["PREDID"].Value = pred.PatternMemberID;
                                command.Parameters["OBJID"].Value  = lit.PatternMemberID;
                            }
                            else {
                                //C->->P->
                                command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ContextID\" = :CTXID AND \"PredicateID\" = :PREDID", this.Connection);
                                command.Parameters.Add(new OracleParameter("CTXID",  OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("PREDID", OracleDbType.Int64));
                                command.Parameters["CTXID"].Value  = ctx.PatternMemberID;
                                command.Parameters["PREDID"].Value = pred.PatternMemberID;
                            }
                        }
                    }
                    else {
                        if (obj     != null) {
                            //C->->->O
                            command  = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ContextID\" = :CTXID AND \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                            command.Parameters.Add(new OracleParameter("TFV",   OracleDbType.Int32));
                            command.Parameters.Add(new OracleParameter("CTXID", OracleDbType.Int64));
                            command.Parameters.Add(new OracleParameter("OBJID", OracleDbType.Int64));
                            command.Parameters["TFV"].Value   = (Int32)RDFModelEnums.RDFTripleFlavors.SPO;
                            command.Parameters["CTXID"].Value = ctx.PatternMemberID;
                            command.Parameters["OBJID"].Value = obj.PatternMemberID;
                        }
                        else {
                            if (lit != null) {
                                //C->->->L
                                command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ContextID\" = :CTXID AND \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                                command.Parameters.Add(new OracleParameter("TFV",   OracleDbType.Int32));
                                command.Parameters.Add(new OracleParameter("CTXID", OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("OBJID", OracleDbType.Int64));
                                command.Parameters["TFV"].Value   = (Int32)RDFModelEnums.RDFTripleFlavors.SPL;
                                command.Parameters["CTXID"].Value = ctx.PatternMemberID;
                                command.Parameters["OBJID"].Value = lit.PatternMemberID;
                            }
                            else {
                                //C->->->
                                command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ContextID\" = :CTXID", this.Connection);
                                command.Parameters.Add(new OracleParameter("CTXID", OracleDbType.Int64));
                                command.Parameters["CTXID"].Value = ctx.PatternMemberID;
                            }
                        }
                    }
                }
            }
            else {
                if (subj            != null) {
                    if (pred        != null) {
                        if (obj     != null) {
                            //->S->P->O
                            command  = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"SubjectID\" = :SUBJID AND \"PredicateID\" = :PREDID AND \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                            command.Parameters.Add(new OracleParameter("TFV",    OracleDbType.Int32));
                            command.Parameters.Add(new OracleParameter("SUBJID", OracleDbType.Int64));
                            command.Parameters.Add(new OracleParameter("PREDID", OracleDbType.Int64));
                            command.Parameters.Add(new OracleParameter("OBJID",  OracleDbType.Int64));
                            command.Parameters["TFV"].Value    = (Int32)RDFModelEnums.RDFTripleFlavors.SPO;
                            command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                            command.Parameters["PREDID"].Value = pred.PatternMemberID;
                            command.Parameters["OBJID"].Value  = obj.PatternMemberID;
                        }
                        else {
                            if (lit != null) {
                                //->S->P->L
                                command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"SubjectID\" = :SUBJID AND \"PredicateID\" = :PREDID AND \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                                command.Parameters.Add(new OracleParameter("TFV",    OracleDbType.Int32));
                                command.Parameters.Add(new OracleParameter("SUBJID", OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("PREDID", OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("OBJID",  OracleDbType.Int64));
                                command.Parameters["TFV"].Value    = (Int32)RDFModelEnums.RDFTripleFlavors.SPL;
                                command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                                command.Parameters["PREDID"].Value = pred.PatternMemberID;
                                command.Parameters["OBJID"].Value  = lit.PatternMemberID;
                            }
                            else {
                                //->S->P->
                                command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"SubjectID\" = :SUBJID AND \"PredicateID\" = :PREDID", this.Connection);
                                command.Parameters.Add(new OracleParameter("SUBJID", OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("PREDID", OracleDbType.Int64));
                                command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                                command.Parameters["PREDID"].Value = pred.PatternMemberID;
                            }
                        }
                    }
                    else {
                        if (obj     != null) {
                            //->S->->O
                            command  = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"SubjectID\" = :SUBJID AND \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                            command.Parameters.Add(new OracleParameter("TFV",    OracleDbType.Int32));
                            command.Parameters.Add(new OracleParameter("SUBJID", OracleDbType.Int64));
                            command.Parameters.Add(new OracleParameter("OBJID",  OracleDbType.Int64));
                            command.Parameters["TFV"].Value    = (Int32)RDFModelEnums.RDFTripleFlavors.SPO;
                            command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                            command.Parameters["OBJID"].Value  = obj.PatternMemberID;
                        }
                        else {
                            if (lit != null) {
                                //->S->->L
                                command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"SubjectID\" = :SUBJID AND \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                                command.Parameters.Add(new OracleParameter("TFV",    OracleDbType.Int32));
                                command.Parameters.Add(new OracleParameter("SUBJID", OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("OBJID",  OracleDbType.Int64));
                                command.Parameters["TFV"].Value    = (Int32)RDFModelEnums.RDFTripleFlavors.SPL;
                                command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                                command.Parameters["OBJID"].Value  = lit.PatternMemberID;
                            }
                            else {
                                //->S->->
                                command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"SubjectID\" = :SUBJID", this.Connection);
                                command.Parameters.Add(new OracleParameter("SUBJID", OracleDbType.Int64));
                                command.Parameters["SUBJID"].Value = subj.PatternMemberID;
                            }
                        }
                    }
                }
                else {
                    if (pred        != null) {
                        if (obj     != null) {
                            //->->P->O
                            command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"PredicateID\" = :PREDID AND \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                            command.Parameters.Add(new OracleParameter("TFV",    OracleDbType.Int32));
                            command.Parameters.Add(new OracleParameter("PREDID", OracleDbType.Int64));
                            command.Parameters.Add(new OracleParameter("OBJID",  OracleDbType.Int64));
                            command.Parameters["TFV"].Value    = (Int32)RDFModelEnums.RDFTripleFlavors.SPO;
                            command.Parameters["PREDID"].Value = pred.PatternMemberID;
                            command.Parameters["OBJID"].Value  = obj.PatternMemberID;
                        }
                        else {
                            if (lit != null) {
                                //->->P->L
                                command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"PredicateID\" = :PREDID AND \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                                command.Parameters.Add(new OracleParameter("TFV",    OracleDbType.Int32));
                                command.Parameters.Add(new OracleParameter("PREDID", OracleDbType.Int64));
                                command.Parameters.Add(new OracleParameter("OBJID",  OracleDbType.Int64));
                                command.Parameters["TFV"].Value    = (Int32)RDFModelEnums.RDFTripleFlavors.SPL;
                                command.Parameters["PREDID"].Value = pred.PatternMemberID;
                                command.Parameters["OBJID"].Value  = lit.PatternMemberID;
                            }
                            else {
                                //->->P->
                                command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"PredicateID\" = :PREDID", this.Connection);
                                command.Parameters.Add(new OracleParameter("PREDID", OracleDbType.Int64));
                                command.Parameters["PREDID"].Value = pred.PatternMemberID;
                            }
                        }
                    }
                    else {
                        if (obj     != null) {
                            //->->->O
                            command  = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                            command.Parameters.Add(new OracleParameter("TFV",   OracleDbType.Int32));
                            command.Parameters.Add(new OracleParameter("OBJID", OracleDbType.Int64));
                            command.Parameters["TFV"].Value   = (Int32)RDFModelEnums.RDFTripleFlavors.SPO;
                            command.Parameters["OBJID"].Value = obj.PatternMemberID;
                        }
                        else {
                            if (lit != null) {
                                //->->->L
                                command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\" WHERE \"ObjectID\" = :OBJID AND \"TripleFlavor\" = :TFV", this.Connection);
                                command.Parameters.Add(new OracleParameter("TFV",   OracleDbType.Int32));
                                command.Parameters.Add(new OracleParameter("OBJID", OracleDbType.Int64));
                                command.Parameters["TFV"].Value   = (Int32)RDFModelEnums.RDFTripleFlavors.SPL;
                                command.Parameters["OBJID"].Value = lit.PatternMemberID;
                            }
                            else {
                                //->->->
                                command = new OracleCommand("SELECT \"TripleFlavor\", \"Context\", \"Subject\", \"Predicate\", \"Object\" FROM \"" + this.ConnectionBuilder.UserID + "\".\"Quadruples\"", this.Connection);
                            }
                        }
                    }
                }
            }

            //Prepare and execute command
            try {

                //Open connection
                this.Connection.Open();

                //Prepare command
                command.Prepare();

                //Set command timeout (3min)
                command.CommandTimeout = 180;

                //Execute command
                using  (var quadruples = command.ExecuteReader()) {
                    if (quadruples.HasRows) {
                        while (quadruples.Read()) {
                            result.AddQuadruple(RDFStoreUtilities.ParseQuadruple(quadruples));
                        }
                    }
                }

                //Close connection
                this.Connection.Close();

            }
            catch (Exception ex) {

                //Close connection
                this.Connection.Close();

                //Propagate exception
                throw new RDFStoreException("Cannot read data from Oracle store because: " + ex.Message, ex);

            }

            return result;
        }
        #endregion

		#region Diagnostics
        /// <summary>
        /// Performs the preliminary diagnostics controls on the underlying Oracle database
        /// </summary>
        private RDFStoreEnums.RDFStoreSQLErrors Diagnostics() {
            try {

                //Open connection
                this.Connection.Open();

                //Create command
                var command     = new OracleCommand("SELECT COUNT(*) FROM all_objects WHERE object_type in ('TABLE', 'VIEW') AND object_name = 'Quadruples'", this.Connection);

                //Execute command
                var result      = Int32.Parse(command.ExecuteScalar().ToString());

                //Close connection
                this.Connection.Close();

                //Return the diagnostics state
                if (result     == 0) {
                    return RDFStoreEnums.RDFStoreSQLErrors.QuadruplesTableNotFound;
                }
                else {
                    return RDFStoreEnums.RDFStoreSQLErrors.NoErrors;
                }

            }
            catch {

                //Close connection
                this.Connection.Close();

                //Return the diagnostics state
                return RDFStoreEnums.RDFStoreSQLErrors.InvalidDataSource;

            }
        }

        /// <summary>
        /// Prepares the underlying Oracle database
        /// </summary>
        private void PrepareStore() {
            var check           = this.Diagnostics();

            //Prepare the database only if diagnostics has detected the missing of "Quadruples" table in the store
            if (check          == RDFStoreEnums.RDFStoreSQLErrors.QuadruplesTableNotFound) {
                try {

                    //Open connection
                    this.Connection.Open();

                    //Create & Execute command
                    var command         = new OracleCommand("CREATE TABLE \"Quadruples\"(\"QuadrupleID\" NUMBER(19, 0) NOT NULL ENABLE,\"TripleFlavor\" NUMBER(10, 0) NOT NULL ENABLE,\"ContextID\" NUMBER(19, 0) NOT NULL ENABLE,\"Context\" VARCHAR2(1000) NOT NULL ENABLE,\"SubjectID\" NUMBER(19, 0) NOT NULL ENABLE,\"Subject\" VARCHAR2(1000) NOT NULL ENABLE,\"PredicateID\" NUMBER(19, 0) NOT NULL ENABLE,\"Predicate\" VARCHAR2(1000) NOT NULL ENABLE,\"ObjectID\" NUMBER(19, 0) NOT NULL ENABLE,\"Object\" VARCHAR2(1000) NOT NULL ENABLE,PRIMARY KEY(\"QuadrupleID\") ENABLE)", this.Connection);
                    command.ExecuteNonQuery();
                    command.CommandText = "CREATE INDEX \"IDX_ContextID\"             ON \"Quadruples\"(\"ContextID\")";
                    command.ExecuteNonQuery();
                    command.CommandText = "CREATE INDEX \"IDX_SubjectID\"             ON \"Quadruples\"(\"SubjectID\")";
                    command.ExecuteNonQuery();
                    command.CommandText = "CREATE INDEX \"IDX_PredicateID\"           ON \"Quadruples\"(\"PredicateID\")";
                    command.ExecuteNonQuery();
                    command.CommandText = "CREATE INDEX \"IDX_ObjectID\"              ON \"Quadruples\"(\"ObjectID\",\"TripleFlavor\")";
                    command.ExecuteNonQuery();
                    command.CommandText = "CREATE INDEX \"IDX_SubjectID_PredicateID\" ON \"Quadruples\"(\"SubjectID\",\"PredicateID\")";
                    command.ExecuteNonQuery();
                    command.CommandText = "CREATE INDEX \"IDX_SubjectID_ObjectID\"    ON \"Quadruples\"(\"SubjectID\",\"ObjectID\",\"TripleFlavor\")";
                    command.ExecuteNonQuery();
                    command.CommandText = "CREATE INDEX \"IDX_PredicateID_ObjectID\"  ON \"Quadruples\"(\"PredicateID\",\"ObjectID\",\"TripleFlavor\")";
                    command.ExecuteNonQuery();

                    //Close connection
                    this.Connection.Close();

                }
                catch (Exception ex) {

                    //Close connection
                    this.Connection.Close();

                    //Propagate exception
                    throw new RDFStoreException("Cannot prepare Oracle store because: " + ex.Message, ex);

                }
            }

            //Otherwise, an exception must be thrown because it has not been possible to connect to the instance/database
            else if (check     == RDFStoreEnums.RDFStoreSQLErrors.InvalidDataSource) {
                throw new RDFStoreException("Cannot prepare Oracle store because: unable to connect to the server instance or to open the selected database.");
            }
        }
        #endregion

        #endregion

    }

}